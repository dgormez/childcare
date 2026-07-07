import { renderHook } from "@testing-library/react-native";
import * as RN from "react-native";
import { useColors } from "../../hooks/useColors";

it("returns dark values when scheme is dark", async () => {
  jest.spyOn(RN, "useColorScheme").mockReturnValue("dark");
  const { result } = await renderHook(() => useColors());
  expect(result.current.surface).toBe("#262220");
  expect(result.current.text).toBe("#F5F1EA");
});

it("returns light values when scheme is light", async () => {
  jest.spyOn(RN, "useColorScheme").mockReturnValue("light");
  const { result } = await renderHook(() => useColors());
  expect(result.current.surface).toBe("#FFFFFF");
  expect(result.current.text).toBe("#1F2937");
});
