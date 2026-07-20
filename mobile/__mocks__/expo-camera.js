const React = require("react");
const { View } = require("react-native");

function CameraView(props) {
  return React.createElement(View, { testID: "camera-view", ...props });
}

module.exports = {
  CameraView,
  useCameraPermissions: jest.fn(() => [{ granted: true, canAskAgain: true }, jest.fn(), jest.fn()]),
};
