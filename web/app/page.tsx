import { redirect } from "next/navigation";

// Root → redirect to the default authenticated screen (Dashboard, feature 013c — the first
// screen in this app with dashboard-shaped content); (app)/layout.tsx redirects to /login if
// there's no session.
export default function Home() {
  redirect("/dashboard");
}
