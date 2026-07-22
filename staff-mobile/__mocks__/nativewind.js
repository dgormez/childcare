const { useColorScheme: rnUseColorScheme } = require("react-native");

module.exports = {
  useColorScheme: () => ({
    colorScheme: rnUseColorScheme() ?? "light",
    setColorScheme: jest.fn(),
    toggleColorScheme: jest.fn(),
  }),
  styled: (Component) => Component,
  cssInterop: () => {},
  remapProps: () => {},
};
