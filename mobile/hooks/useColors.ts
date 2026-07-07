import { useColorScheme } from "nativewind";
import { light, dark } from "../theme/colors";

/**
 * Raw hex tokens for the handful of call sites that can't use a Tailwind className
 * (StatusBar tint, native Image tint, dynamically-built inline styles). Everything
 * else should use the bg-x/text-x classNames from tailwind.config.js instead — both
 * read from the same theme/colors.js, so there's one place to change a value.
 */
export function useColors() {
  const { colorScheme } = useColorScheme();
  return colorScheme === "dark" ? dark : light;
}
