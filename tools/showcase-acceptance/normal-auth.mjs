import assert from "node:assert/strict";

const base = "http://localhost/api/auth";
const email = `acceptance-${Date.now()}@omnidoc.test`;
const password = "Local-Acceptance2026!";
let token;
async function call(path, body) {
  const r = await fetch(base + path, { method: "POST", headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify(body) });
  assert.ok(r.ok, `${path}: HTTP ${r.status}`);
  return r.json();
}
const registered = await call("/register", { email, password, fullName: "Local acceptance test" });
token = registered.token;
assert.ok(token && !registered.emailConfirmed);
const otp = await call("/send-verification-otp", {});
assert.ok(otp.debugOtp, "This local-only check requires EMAIL_SHOW_DEMO_OTP=true");
assert.ok((await call("/verify-email", { otp: otp.debugOtp })).emailConfirmed);
const changed = "Changed-Acceptance2026!";
token = (await call("/change-password", { currentPassword: password, newPassword: changed })).token;
assert.ok(token);
const reset = await call("/forgot-password", { email });
assert.ok(reset.debugResetUrl);
const resetToken = new URL(reset.debugResetUrl, "http://localhost").searchParams.get("token");
await call("/reset-password", { email, token: resetToken, newPassword: password });
token = undefined;
assert.ok((await call("/login", { email, password })).token);
// Verify actual SMTP delivery in the local Mailpit inbox, not only the debug response.
let received = false;
for (let i = 0; i < 30; i++) {
  const inbox = await (await fetch("http://localhost:8025/api/v1/messages?limit=50")).json();
  received = inbox.messages.some(m => m.To.some(recipient => recipient.Address === email));
  if (received) break;
  await new Promise(resolve => setTimeout(resolve, 2000));
}
assert.ok(received, "Test email must arrive in Mailpit");
console.log("PASS personal register, OTP verification, change/reset password, real login and Mailpit delivery (no tokens logged)");
