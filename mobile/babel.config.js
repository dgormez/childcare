module.exports = function (api) {
  api.cache(true);
  return {
    presets: [
      ['babel-preset-expo', {
        jsxImportSource: 'nativewind',
        unstable_transformProfile: 'hermes-v0',
      }],
    ],
    plugins: ['react-native-reanimated/plugin'],
    overrides: [
      {
        test: /node_modules\/react-native-worklets/,
        plugins: [
          '@babel/plugin-transform-class-properties',
          '@babel/plugin-transform-private-methods',
          '@babel/plugin-transform-private-property-in-object',
        ],
      },
    ],
  };
};
