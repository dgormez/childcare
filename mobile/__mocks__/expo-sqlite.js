const db = {
  execSync:    jest.fn(),
  runSync:     jest.fn(),
  getFirstSync: jest.fn(() => null),
  getAllSync:   jest.fn(() => []),
  prepareSync:  jest.fn(() => ({
    executeSync: jest.fn(),
    finalizeSync: jest.fn(),
  })),
};
module.exports = { openDatabaseSync: jest.fn(() => db) };
