import { cn } from "@/lib/utils";

const TONS = {
  neutre: "bg-neutre-fond text-neutre-texte",
  orange: "bg-orange-fond text-orange-texte",
  vert: "bg-vert-fond text-vert-texte",
  rouge: "bg-rouge-fond text-rouge-texte",
  violet: "bg-violet-fond text-violet-texte",
} as const;

export function Badge({ ton = "neutre", className, children }: {
  ton?: keyof typeof TONS; className?: string; children: React.ReactNode;
}) {
  return (
    <span className={cn("inline-flex items-center rounded-full px-2.5 py-0.5 text-[12px] font-semibold", TONS[ton], className)}>
      {children}
    </span>
  );
}
