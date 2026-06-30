"use client";

// Catches errors thrown by any page inside the (app) group.
// Renders within the existing app layout so the sidebar stays visible.
export default function AppError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="flex items-center justify-center h-96">
      <div className="text-center space-y-3 max-w-sm">
        <p className="text-3xl">⚠️</p>
        <h2 className="text-lg font-bold text-gray-900">Something went wrong</h2>
        <p className="text-gray-500 text-sm">This page hit an error. Your data is safe.</p>
        <button
          onClick={reset}
          className="bg-blue-600 hover:bg-blue-700 text-white font-semibold px-6 py-3 rounded-xl transition text-sm"
        >
          Try again
        </button>
      </div>
    </div>
  );
}
