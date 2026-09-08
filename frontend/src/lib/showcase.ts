// Public demo credentials only. The showcase seeder must use the same values.
// NEXT_PUBLIC_* values are embedded at build time and are not secrets.
export const showcase = {
  enabled: process.env.NEXT_PUBLIC_SHOWCASE_ENABLED !== "false",
  email: process.env.NEXT_PUBLIC_SHOWCASE_EMAIL || "recruiter@omnidoc.io",
  password: process.env.NEXT_PUBLIC_SHOWCASE_PASSWORD || "OmniDoc-Showcase2026!",
};
