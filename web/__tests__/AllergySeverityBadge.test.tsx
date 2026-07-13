import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { AllergySeverityBadge } from "../components/meal-list/AllergySeverityBadge";

function renderBadge(severity: "severe" | "mild_moderate" | "none") {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <AllergySeverityBadge severity={severity} />
    </NextIntlClientProvider>,
  );
}

describe("AllergySeverityBadge", () => {
  it("renders a distinct icon (svg path shape) per severity level, not color alone", () => {
    const severe = renderBadge("severe");
    const severeSvg = severe.container.querySelector("svg")!;

    const mild = renderBadge("mild_moderate");
    const mildSvg = mild.container.querySelector("svg")!;

    const none = renderBadge("none");
    const noneSvg = none.container.querySelector("svg")!;

    // Each severity uses a different lucide icon component — verified by each svg having a
    // distinct set of child path/circle elements (shape), not merely a different className.
    expect(severeSvg.innerHTML).not.toEqual(mildSvg.innerHTML);
    expect(mildSvg.innerHTML).not.toEqual(noneSvg.innerHTML);
    expect(severeSvg.innerHTML).not.toEqual(noneSvg.innerHTML);
  });

  it("renders the correct label text for each severity", () => {
    expect(renderBadge("severe").getByText(messages.mealList.allergySeverity.severe)).toBeTruthy();
    expect(renderBadge("mild_moderate").getByText(messages.mealList.allergySeverity.mild_moderate)).toBeTruthy();
    expect(renderBadge("none").getByText(messages.mealList.allergySeverity.none)).toBeTruthy();
  });
});
