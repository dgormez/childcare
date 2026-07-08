import type { LucideIcon } from "lucide-react";

/** design-system.md: an empty state is an icon + one short human sentence — no illustration
 * library. */
export function EmptyState({ icon: Icon, message }: { icon: LucideIcon; message: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <Icon className="h-6 w-6 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{message}</p>
    </div>
  );
}
