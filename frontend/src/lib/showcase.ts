// Public demo credentials only. The showcase seeder must use the same values.
// NEXT_PUBLIC_* values are embedded at build time and are not secrets.
export const showcase = {
  enabled: process.env.NEXT_PUBLIC_SHOWCASE_ENABLED !== "false",
  email: process.env.NEXT_PUBLIC_SHOWCASE_EMAIL || "recruiter@omnidoc.io",
  password: process.env.NEXT_PUBLIC_SHOWCASE_PASSWORD || "OmniDoc-Showcase2026!",
  workspaceId: process.env.NEXT_PUBLIC_SHOWCASE_WORKSPACE_ID?.trim() || null,
};

export function isShowcaseUser(user: { email: string } | null) {
  return showcase.enabled && user?.email.trim().toLowerCase() === showcase.email.trim().toLowerCase();
}

export function getShowcaseWorkspaceId(workspaces: { id: string; role: string }[]) {
  return showcase.workspaceId ?? workspaces.find((workspace) => workspace.role === "Owner")?.id ?? null;
}

export const SHOWCASE_PROMPTS = [
  "Doanh thu quý III đạt bao nhiêu tỷ đồng?",
  "Ai phê duyệt đề nghị mua sắm trước khi chuyển đến phòng tài chính?",
  "Lộ trình triển khai tháng 10, 11, 12 gồm những bước nào?",
];
