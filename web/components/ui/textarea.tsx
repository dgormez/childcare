import * as React from "react";
import { cn } from "../../lib/cn";

/** design-system.md Forms: surface-soft fill, 8px radius, no visible border unless invalid —
 * same treatment as Input, multi-line. */
const Textarea = React.forwardRef<HTMLTextAreaElement, React.TextareaHTMLAttributes<HTMLTextAreaElement> & { invalid?: boolean }>(
  ({ className, invalid, ...props }, ref) => {
    return (
      <textarea
        ref={ref}
        aria-invalid={invalid}
        className={cn(
          "flex w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text placeholder:text-placeholder focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-50 dark:bg-surface-soft-dark dark:text-text-dark dark:placeholder:text-placeholder-dark",
          invalid && "ring-2 ring-danger dark:ring-danger-dark",
          className,
        )}
        {...props}
      />
    );
  },
);
Textarea.displayName = "Textarea";

export { Textarea };
