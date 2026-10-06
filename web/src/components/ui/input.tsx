import * as React from "react";
import { cn } from "@/lib/utils";

const base =
  "h-10 w-full rounded-lg border border-bordure-forte bg-white px-3 text-sm text-texte placeholder:text-attenue focus:border-marine focus:outline-none focus:ring-2 focus:ring-marine/15 disabled:bg-fond aria-[invalid=true]:border-rouge-vif";

export const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => <input ref={ref} className={cn(base, className)} {...props} />,
);
Input.displayName = "Input";

export const Select = React.forwardRef<HTMLSelectElement, React.SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, ...props }, ref) => <select ref={ref} className={cn(base, "pr-8", className)} {...props} />,
);
Select.displayName = "Select";

export function Field({ label, error, required, children, className }: {
  label: string; error?: string; required?: boolean; children: React.ReactNode; className?: string;
}) {
  return (
    <label className={cn("block", className)}>
      <span className="mb-1.5 block text-[13px] font-semibold text-secondaire">
        {label}{required && <span className="text-rouge-vif"> *</span>}
      </span>
      {children}
      {error && <span className="mt-1 block text-[12px] text-rouge-texte">{error}</span>}
    </label>
  );
}
