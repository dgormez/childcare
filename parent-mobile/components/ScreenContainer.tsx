import React from "react";
import { View } from "react-native";
import { useBreakpoint } from "../hooks/useBreakpoint";

interface Props {
  children:  React.ReactNode;
  maxWidth?: number;
}

export function ScreenContainer({ children, maxWidth = 768 }: Props) {
  const { isWide } = useBreakpoint();
  if (!isWide) return <>{children}</>;
  return (
    <View style={{ flex: 1, alignItems: "center" }}>
      <View style={{ flex: 1, width: "100%", maxWidth }}>
        {children}
      </View>
    </View>
  );
}
