const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind }   = require('nativewind/metro');
const path = require('path');

const config = getDefaultConfig(__dirname);

const originalResolver = config.resolver.resolveRequest;
config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (platform === 'web' && moduleName === 'expo-secure-store') {
    return { filePath: path.resolve(__dirname, 'shims/expo-secure-store.web.js'), type: 'sourceFile' };
  }
  if (originalResolver) return originalResolver(context, moduleName, platform);
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = withNativeWind(config, { input: './global.css' });
