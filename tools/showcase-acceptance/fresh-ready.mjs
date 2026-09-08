import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const modulePath = process.env.PLAYWRIGHT_MODULE || "playwright";
const { chromium } = require(modulePath);
const { expect } = require(modulePath + "/test");
const browser = await chromium.launch({ channel: "msedge", headless: true });
try {
  const page = await browser.newPage();
  await page.goto("http://localhost/login?mode=showcase");
  await page.getByRole("button", { name: "Sử dụng tài khoản Trải nghiệm" }).click();
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL("http://localhost/workspaces/b4987f7e-48cc-4ba5-a117-10ac4cbced02/chat");
  await expect(page.getByRole("button", { name: "Doanh thu quý III đạt bao nhiêu tỷ đồng?", exact: true })).toBeEnabled();
  await expect(page.locator("iframe")).toHaveAttribute("src", /blob:.*#page=1/);
  console.log("PASS rebuilt stack: real quick-fill login, fresh chat and canonical PDF ready; no conversation created");
} finally { await browser.close(); }
