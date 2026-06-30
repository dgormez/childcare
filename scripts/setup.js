#!/usr/bin/env node
'use strict';

/**
 * MiniStack setup script — renames all StackMini/MiniStack tokens to your app name.
 * Run from the repo root: node scripts/setup.js
 * Requires Node 16+. No external dependencies.
 */

const fs   = require('fs');
const path = require('path');
const rl   = require('readline');
const { execSync } = require('child_process');

const ROOT = process.cwd();

// ── Prompts ───────────────────────────────────────────────────────────────────

function ask(iface, question) {
  return new Promise(resolve => iface.question(question, answer => resolve(answer.trim())));
}

async function collectInputs() {
  const iface = rl.createInterface({ input: process.stdin, output: process.stdout });

  console.log('\nMiniStack Setup\n' + '─'.repeat(44) + '\n');

  let appName;
  while (true) {
    appName = await ask(iface, 'App name (PascalCase, e.g. FitTrack): ');
    if (/^[A-Z][A-Za-z0-9]+$/.test(appName)) break;
    console.log('  Must start with a capital letter, no spaces or special characters.\n');
  }

  let bundleId;
  while (true) {
    bundleId = await ask(iface, 'Bundle ID  (e.g. com.acme.fittrack):  ');
    if (/^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$/.test(bundleId)) break;
    console.log('  Must be lowercase reverse-domain format, e.g. com.acme.fittrack\n');
  }

  iface.close();
  return { appName, bundleId };
}

// ── File walker ───────────────────────────────────────────────────────────────

const SKIP_DIRS = new Set([
  'node_modules', '.git', 'bin', 'obj', 'Pods',
  '.next', 'build', '.expo', '.terraform',
]);

const TEXT_EXTENSIONS = new Set([
  '.ts', '.tsx', '.js', '.json', '.cs', '.csproj', '.sln',
  '.yml', '.yaml', '.tf', '.tfvars', '.gradle', '.xml',
  '.plist', '.pbxproj', '.md', '.sh', '.txt', '.example',
]);

const TEXT_BASENAMES = new Set(['Dockerfile']);

const SKIP_FILES = new Set(['package-lock.json', 'setup.js']);

function walkFiles(dir, callback) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIP_DIRS.has(entry.name)) walkFiles(fullPath, callback);
    } else if (entry.isFile()) {
      callback(fullPath, entry.name);
    }
  }
}

// ── Text replacement ──────────────────────────────────────────────────────────

function replaceInFile(filePath, replacements) {
  const baseName = path.basename(filePath);
  if (SKIP_FILES.has(baseName)) return;

  const ext = path.extname(filePath).toLowerCase();
  if (!TEXT_EXTENSIONS.has(ext) && !TEXT_BASENAMES.has(baseName)) return;

  let content;
  try {
    content = fs.readFileSync(filePath, 'utf8');
  } catch {
    return;
  }

  // Quick exit if none of the source tokens are present
  if (!replacements.some(([from]) => content.includes(from))) return;

  let updated = content;
  for (const [from, to] of replacements) {
    updated = updated.split(from).join(to);
  }

  if (updated !== content) fs.writeFileSync(filePath, updated, 'utf8');
}

// ── Rename helpers ────────────────────────────────────────────────────────────

function renameFile(src, dst) {
  src = path.join(ROOT, src);
  dst = path.join(ROOT, dst);
  if (fs.existsSync(src) && src !== dst) fs.renameSync(src, dst);
}

function renameDir(src, dst) {
  src = path.join(ROOT, src);
  dst = path.join(ROOT, dst);
  if (!fs.existsSync(src) || src === dst) return;

  if (fs.existsSync(dst)) {
    // Destination exists (re-run) — merge src into dst
    for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
      const srcEntry = path.join(src, entry.name);
      const dstEntry = path.join(dst, entry.name);
      if (!fs.existsSync(dstEntry)) fs.renameSync(srcEntry, dstEntry);
    }
    fs.rmSync(src, { recursive: true, force: true });
  } else {
    fs.renameSync(src, dst);
  }
}

// ── Git ───────────────────────────────────────────────────────────────────────

function reinitGit(appName) {
  const gitDir = path.join(ROOT, '.git');
  if (fs.existsSync(gitDir)) fs.rmSync(gitDir, { recursive: true, force: true });

  try {
    execSync('git init',    { cwd: ROOT, stdio: 'pipe' });
    execSync('git add -A',  { cwd: ROOT, stdio: 'pipe' });
    execSync(`git commit -m "Initial commit — ${appName}"`, { cwd: ROOT, stdio: 'pipe' });
    console.log('  ✓ Git history reset with a clean initial commit.');
  } catch (err) {
    console.log('  ! Git reinit skipped (git not available or commit failed). Run manually if needed.');
  }
}

// ── Main ──────────────────────────────────────────────────────────────────────

async function main() {
  // Guard: must be run from the repo root, not from inside scripts/
  const requiredDirs = ['mobile', 'backend', 'web', 'scripts'];
  const missingDirs  = requiredDirs.filter(d => !fs.existsSync(path.join(ROOT, d)));
  if (missingDirs.length > 0) {
    console.error('\nError: run this script from the repo root, not from inside scripts/');
    console.error('  cd .. && node scripts/setup.js\n');
    process.exit(1);
  }

  const { appName, bundleId } = await collectInputs();
  const slug = bundleId.split('.').pop(); // com.acme.fittrack → fittrack

  const appNameLower = appName.toLowerCase();

  console.log(`\nSetting up ${appName} (bundle: ${bundleId}, slug: ${slug})...\n`);

  // Replacement pairs — order matters: more specific strings first.
  const replacements = [
    ['com.company.stackmini', bundleId],
    ['StackMini',             appName],
    ['stackmini-mobile',      `${slug}-mobile`],
    ['stackmini-web',         `${slug}-web`],
    ['stackmini',             slug],
  ];

  // 1. Shut down dotnet build server so it releases file locks (matters on Windows)
  try {
    execSync('dotnet build-server shutdown', { stdio: 'pipe' });
  } catch { /* dotnet not on PATH or already stopped — fine */ }

  // 2. Text replacements across all source files
  process.stdout.write('  Replacing tokens in source files...');
  walkFiles(ROOT, filePath => replaceInFile(filePath, replacements));
  console.log(' done.');

  // 3. Rename any file whose basename contains the old tokens
  process.stdout.write('  Renaming files...');
  const fileTokens = [
    ['StackMini', appName],
    ['stackmini', slug],
  ];
  walkFiles(ROOT, filePath => {
    const dir      = path.dirname(filePath);
    const baseName = path.basename(filePath);
    const skipped  = SKIP_FILES.has(baseName);
    if (skipped) return;
    let newName = baseName;
    for (const [from, to] of fileTokens) newName = newName.split(from).join(to);
    if (newName !== baseName) fs.renameSync(filePath, path.join(dir, newName));
  });
  console.log(' done.');

  // 4. Rename any directory whose name contains the old tokens (deepest first)
  process.stdout.write('  Renaming directories...');
  const tokenizedDirs = [];
  (function collectDirs(dir) {
    let entries;
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      const fullPath = path.join(dir, entry.name);
      if (SKIP_DIRS.has(entry.name)) continue;
      collectDirs(fullPath);
      tokenizedDirs.push(fullPath);
    }
  }(ROOT));
  // collectDirs pushes children before parents, so iterating forward = deepest first
  for (const dirPath of tokenizedDirs) {
    const parent  = path.dirname(dirPath);
    const name    = path.basename(dirPath);
    let newName   = name;
    for (const [from, to] of fileTokens) newName = newName.split(from).join(to);
    if (newName !== name) {
      const dst = path.join(parent, newName);
      if (!fs.existsSync(dst)) fs.renameSync(dirPath, dst);
    }
  }
  console.log(' done.');

  // 5. Reset git history
  process.stdout.write('  Reinitializing git...');
  reinitGit(appName);

  // 6. Summary
  console.log(`
─────────────────────────────────────────────
  ${appName} is ready. Next steps:
─────────────────────────────────────────────

  1. Copy env example files and fill in your credentials:
       mobile/.env.example         →  mobile/.env
       web/.env.local.example      →  web/.env.local
       backend/${appName}.Api/appsettings.Development.example.json
                                   →  appsettings.Development.json

  2. Work through SETUP_CHECKLIST.md

  3. Create your GitHub repo and push:
       git remote add origin https://github.com/you/${appName.toLowerCase()}.git
       git push -u origin master
`);
}

main().catch(err => {
  console.error('\nSetup failed:', err.message);
  process.exit(1);
});
