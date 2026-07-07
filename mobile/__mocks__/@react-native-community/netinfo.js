let listeners = [];
let currentState = { isConnected: true, isInternetReachable: true };

function addEventListener(listener) {
  listeners.push(listener);
  listener(currentState);
  return () => {
    listeners = listeners.filter((l) => l !== listener);
  };
}

function fetch() {
  return Promise.resolve(currentState);
}

// Test helper — not part of the real NetInfo API, used by tests to simulate connectivity
// changes (jest.requireMock("@react-native-community/netinfo").__setConnected(false)).
function __setConnected(isConnected) {
  currentState = { isConnected, isInternetReachable: isConnected };
  listeners.forEach((l) => l(currentState));
}

module.exports = {
  addEventListener,
  fetch,
  __setConnected,
};
