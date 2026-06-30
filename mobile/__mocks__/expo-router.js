module.exports = {
  useRouter:            jest.fn(() => ({ push: jest.fn(), replace: jest.fn(), back: jest.fn() })),
  useLocalSearchParams: jest.fn(() => ({})),
  Redirect:             () => null,
  Stack:                { Screen: () => null },
  Tabs:                 { Screen: () => null },
  Link:                 ({ children }) => children,
};
