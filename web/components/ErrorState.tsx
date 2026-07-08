import { AlertTriangle } from "lucide-react";
import { Button } from "./ui/button";

/** design-system.md/constitution VI: never expose a raw error or stack trace — a clear,
 * human-readable, localized message plus a retry action (FR-012/FR-016). */
export function ErrorState({ message, retryLabel, onRetry }: { message: string; retryLabel: string; onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <AlertTriangle className="h-6 w-6 text-danger dark:text-danger-dark" strokeWidth={2} />
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{message}</p>
      <Button variant="secondary" size="sm" onClick={onRetry}>
        {retryLabel}
      </Button>
    </div>
  );
}
