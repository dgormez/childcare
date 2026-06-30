/**
 * Custom Jest transformer that patches react-native/jest/mockComponent.js
 * to guard against `RealComponent.prototype` being undefined in RN 0.81.5+
 * (functional components created with the new `component()` syntax have no prototype).
 */
const babelJest = require("babel-jest");

const transformer = babelJest.createTransformer({
  caller: { name: "metro", bundler: "metro", platform: "ios" },
});

module.exports = {
  process(sourceText, sourcePath, options) {
    // Patch the mockComponent.js prototype check before Babel transforms it
    if (sourcePath.includes("react-native/jest/mockComponent")) {
      sourceText = sourceText.replace(
        "RealComponent.prototype.constructor instanceof React.Component",
        "RealComponent.prototype != null && RealComponent.prototype.constructor instanceof React.Component"
      );
    }
    return transformer.process(sourceText, sourcePath, options);
  },
  getCacheKey(...args) {
    return transformer.getCacheKey(...args);
  },
};
