function Stack({ children }) { return children ?? null; }
Stack.Screen = () => null;

function Tabs({ children }) { return children ?? null; }
Tabs.Screen = () => null;

module.exports = {
  useRouter:            jest.fn(() => ({ push: jest.fn(), replace: jest.fn(), back: jest.fn() })),
  useLocalSearchParams: jest.fn(() => ({})),
  Redirect:             () => null,
  Stack,
  Tabs,
  Link:                 ({ children }) => children,
};
