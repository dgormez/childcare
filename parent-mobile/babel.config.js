module.exports = function (api) {
  api.cache(true);
  return {
    presets: [
      ['babel-preset-expo', {
        jsxImportSource: 'nativewind',
        unstable_transformProfile: 'hermes-v0',
      }],
    ],
    // Not used for any app-level animation — this app has none — but nativewind pulls in
    // react-native-css-interop, which declares react-native-reanimated as a required (non-
    // optional) peer dependency. Without this plugin present, Metro's real bundler build fails
    // the same way Jest failed until this was added (found while wiring this scaffold: npm
    // nested an incompatible second copy of react-native under nativewind/node_modules to
    // satisfy the unmet peer, which broke every native-module mock in tests).
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
