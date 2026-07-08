import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "../../lib/cn";

/** design-system.md: a badge is a pill (full round) for a single-word/short state attached to
 * an item — semantic colors only, never a per-surface accent. */
const badgeVariants = cva("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium", {
  variants: {
    variant: {
      neutral: "bg-surface-soft text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark",
      success: "bg-success-bg text-success dark:bg-success-bg-dark dark:text-success-dark",
      danger: "bg-danger-bg text-danger dark:bg-danger-bg-dark dark:text-danger-dark",
    },
  },
  defaultVariants: { variant: "neutral" },
});

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
