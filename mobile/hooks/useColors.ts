import { useColorScheme } from "nativewind";

export function useColors() {
  const { colorScheme } = useColorScheme();
  const dark = colorScheme === "dark";
  return {
    placeholder:   dark ? "#6b7280" : "#9ca3af",
    tabBar:        dark ? "#1f2937" : "#ffffff",
    tabBorder:     dark ? "#374151" : "#e5e7eb",
    header:        dark ? "#1f2937" : "#ffffff",
    headerText:    dark ? "#ffffff" : "#111827",
    background:    dark ? "#111827" : "#f9fafb",
    card:          dark ? "#1f2937" : "#ffffff",
    text:          dark ? "#f9fafb" : "#111827",
    secondaryText: dark ? "#9ca3af" : "#6b7280",
    border:        dark ? "#374151" : "#e5e7eb",
  };
}
