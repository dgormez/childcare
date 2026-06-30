import { redirect } from "next/navigation";

// Root → redirect to today's habits
export default function Home() {
  redirect("/habits");
}
