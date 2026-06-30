"use client";

// Catches errors thrown by the root layout itself.
// Must render its own <html> and <body> since it replaces the layout entirely.
export default function GlobalError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en">
      <body className="bg-gray-50 text-gray-900 antialiased min-h-screen flex items-center justify-center px-4">
        <div className="text-center space-y-4 max-w-sm">
          <h2 className="text-xl font-bold">Something went wrong</h2>
          <p className="text-gray-500 text-sm">An unexpected error occurred.</p>
          <button
            onClick={reset}
            className="bg-blue-600 hover:bg-blue-700 text-white font-semibold px-6 py-3 rounded-xl transition"
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
