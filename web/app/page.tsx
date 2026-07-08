import { redirect } from "next/navigation";

// Root → redirect to the default authenticated screen (Staff); (app)/layout.tsx redirects to
// /login if there's no session.
export default function Home() {
  redirect("/staff");
}
