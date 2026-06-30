"use client";

export default function Error({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-gray-50">
      <div className="text-center space-y-4 max-w-sm">
        <h2 className="text-xl font-bold text-gray-900">Something went wrong</h2>
        <p className="text-gray-500 text-sm">
          An unexpected error occurred. Try refreshing the page or click below to retry.
        </p>
        <button
          onClick={reset}
          className="bg-blue-600 hover:bg-blue-700 text-white font-semibold px-6 py-3 rounded-xl transition"
        >
          Try again
        </button>
      </div>
    </div>
  );
}
