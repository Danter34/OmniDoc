import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import { PortfolioLanding } from "@/components/landing/portfolio-landing";
import { SESSION_COOKIE } from "@/lib/session";

export default async function HomePage() {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (token) {
    const backendUrl = (process.env.OMNIDOC_API_URL ?? "http://localhost:5151").replace(/\/$/, "");
    // The API checks the signature, expiry and session version against the user.
    const response = await fetch(`${backendUrl}/api/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    }).catch(() => null);

    if (response?.ok) redirect("/workspaces");
  }

  return <PortfolioLanding />;
}
