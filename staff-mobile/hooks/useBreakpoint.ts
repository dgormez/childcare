import { useWindowDimensions } from "react-native";

export function useBreakpoint() {
  const { width } = useWindowDimensions();
  return {
    width,
    isWide:    width >= 640,
    isMobile:  width < 640,
    isTablet:  width >= 640 && width < 1024,
    isDesktop: width >= 1024,
  };
}
