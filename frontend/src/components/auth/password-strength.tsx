import { cn } from "@/lib/utils";

function calculateStrength(password: string) {
  if (!password) return 0;

  return [
    password.length >= 8,
    password.length >= 12,
    /[a-z]/.test(password) && /[A-Z]/.test(password),
    /\d/.test(password),
    /[^A-Za-z0-9]/.test(password),
  ].filter(Boolean).length;
}

const labels = ["", "Rất yếu", "Yếu", "Trung bình", "Mạnh", "Rất mạnh"];

export function PasswordStrength({ password }: { password: string }) {
  const strength = calculateStrength(password);

  return (
    <div className="mt-2" aria-live="polite">
      <div
        aria-label={`Độ mạnh mật khẩu: ${labels[strength] || "chưa đánh giá"}`}
        aria-valuemax={5}
        aria-valuemin={0}
        aria-valuenow={strength}
        className="grid grid-cols-5 gap-1.5"
        role="progressbar"
      >
        {Array.from({ length: 5 }, (_, index) => (
          <span
            className={cn(
              "h-1.5 rounded-full transition-colors",
              index >= strength
                ? "bg-slate-200"
                : strength <= 2
                  ? "bg-rose-500"
                  : strength === 3
                    ? "bg-amber-500"
                    : "bg-emerald-500",
            )}
            key={index}
          />
        ))}
      </div>
      <div className="mt-1.5 flex items-center justify-between text-xs">
        <span className="text-slate-400">Tối thiểu 8 ký tự</span>
        <span
          className={cn(
            "font-medium",
            strength <= 2
              ? "text-rose-600"
              : strength === 3
                ? "text-amber-600"
                : "text-emerald-600",
          )}
        >
          {labels[strength]}
        </span>
      </div>
    </div>
  );
}
