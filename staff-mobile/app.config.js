// EAS sets EAS_BUILD_PROFILE during cloud builds. Locally it is undefined (treated as dev).
const isProduction = process.env.EAS_BUILD_PROFILE === "production";

/** @type {import('expo/config').ExpoConfig} */
module.exports = {
  expo: {
    name: "ChildCare Staff",
    slug: "childcare-staff",
    version: "1.0.0",
    orientation: "portrait",
    icon: "./assets/images/icon.png",
    userInterfaceStyle: "automatic",
    newArchEnabled: true,
    splash: {
      image: "./assets/images/splash-icon.png",
      resizeMode: "contain",
      backgroundColor: "#FAF9F6",
    },
    ios: {
      supportsTablet: false,
      bundleIdentifier: "com.dgit.childcarestaff",
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false,
        // Allows plain HTTP to reach a local dev server. Omitted in production builds.
        ...(isProduction ? {} : { NSAppTransportSecurity: { NSAllowsArbitraryLoads: true } }),
        // Deep links: childcarestaff:// (used by expo-router — feature 027, no email-link
        // consumer yet, matching parent-mobile's CFBundleURLTypes shape for future use).
        CFBundleURLTypes: [{ CFBundleURLSchemes: ["childcarestaff"] }],
      },
    },
    android: {
      package: "com.dgit.childcarestaff",
      versionCode: 1,
      softwareKeyboardLayoutMode: "pan",
      adaptiveIcon: {
        foregroundImage: "./assets/images/adaptive-icon.png",
        backgroundColor: "#FAF9F6",
      },
    },
    plugins: [
      [
        "expo-build-properties",
        {
          android: {
            // Allows plain HTTP to reach a local dev server. Disabled in production builds.
            usesCleartextTraffic: !isProduction,
          },
        },
      ],
      "expo-router",
      "expo-secure-store",
      [
        "expo-notifications",
        {
          icon: "./assets/images/icon.png",
          color: "#4F7CAC",
          sounds: [],
        },
      ],
    ],
    web: {
      bundler: "metro",
      output: "static",
    },
    experiments: {
      typedRoutes: true,
    },
    scheme: "childcarestaff",
    extra: {
      router: {},
      eas: {
        projectId: "YOUR_EAS_PROJECT_ID",
      },
    },
  },
};
