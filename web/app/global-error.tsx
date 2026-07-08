"use client";

// Catches errors thrown by the root layout itself. Must render its own <html> and <body> since
// it replaces the layout entirely — the NextIntlClientProvider living in that same root layout
// is not reliably available here, so this one boundary is intentionally not localized (it can
// only ever be reached by a crash in the layout that would otherwise provide the locale).
export default function GlobalError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en">
      <body className="flex min-h-screen items-center justify-center bg-background px-4 text-text antialiased dark:bg-background-dark dark:text-text-dark">
        <div className="max-w-sm space-y-4 text-center">
          <h2 className="text-xl font-bold">Something went wrong</h2>
          <p className="text-sm text-text-soft dark:text-text-soft-dark">An unexpected error occurred.</p>
          <button
            onClick={reset}
            className="rounded-lg bg-primary px-6 py-3 font-semibold text-white transition hover:bg-primary-hover"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
