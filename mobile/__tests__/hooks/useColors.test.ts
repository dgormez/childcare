import { renderHook } from "@testing-library/react-native";
import * as RN from "react-native";
import { useColors } from "../../hooks/useColors";

it("returns dark values when scheme is dark", async () => {
  jest.spyOn(RN, "useColorScheme").mockReturnValue("dark");
  const { result } = await renderHook(() => useColors());
  expect(result.current.tabBar).toBe("#1f2937");
  expect(result.current.headerText).toBe("#ffffff");
});

it("returns light values when scheme is light", async () => {
  jest.spyOn(RN, "useColorScheme").mockReturnValue("light");
  const { result } = await renderHook(() => useColors());
  expect(result.current.tabBar).toBe("#ffffff");
  expect(result.current.headerText).toBe("#111827");
});
