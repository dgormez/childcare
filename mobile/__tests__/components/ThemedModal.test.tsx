import React from "react";
import { render, fireEvent } from "@testing-library/react-native";
import { ThemedModal } from "../../components/ThemedModal";

const noop = jest.fn();

it("renders nothing when config is null", async () => {
  const { queryByText } = await render(<ThemedModal config={null} onDismiss={noop} />);
  expect(queryByText(/.+/)).toBeNull();
});

it("renders title and message", async () => {
  const { getByText } = await render(
    <ThemedModal
      config={{ title: "Oops", message: "Something went wrong", buttons: [] }}
      onDismiss={noop}
    />
  );
  expect(getByText("Oops")).toBeTruthy();
  expect(getByText("Something went wrong")).toBeTruthy();
});

it("fires button callback on press", async () => {
  const onPress = jest.fn();
  const { getByText } = await render(
    <ThemedModal
      config={{ title: "Confirm", buttons: [{ label: "OK", style: "default", onPress }] }}
      onDismiss={noop}
    />
  );
  fireEvent.press(getByText("OK"));
  expect(onPress).toHaveBeenCalledTimes(1);
});

it("renders multiple buttons", async () => {
  const { getByText } = await render(
    <ThemedModal
      config={{
        title: "Delete?",
        buttons: [
          { label: "Cancel",  style: "cancel",      onPress: noop },
          { label: "Delete",  style: "destructive",  onPress: noop },
        ],
      }}
      onDismiss={noop}
    />
  );
  expect(getByText("Cancel")).toBeTruthy();
  expect(getByText("Delete")).toBeTruthy();
});
