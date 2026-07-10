const React = require("react");

function Stack({ children, screenOptions }) {
  const headerRight = screenOptions && screenOptions.headerRight;
  return React.createElement(React.Fragment, null, headerRight ? headerRight() : null, children ?? null);
}
Stack.Screen = () => null;

function Tabs({ children }) { return children ?? null; }
Tabs.Screen = () => null;

module.exports = {
  useRouter:            jest.fn(() => ({ push: jest.fn(), replace: jest.fn(), back: jest.fn() })),
  useLocalSearchParams: jest.fn(() => ({})),
  usePathname:          jest.fn(() => "/"),
  Redirect:             () => null,
  Stack,
  Tabs,
  Link:                 ({ children }) => children,
};
