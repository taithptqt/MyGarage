#!/usr/bin/env node

const { execSync } = require("node:child_process");

const TEXT_EXTENSIONS = new Set([
  ".js",
  ".jsx",
  ".ts",
  ".tsx",
  ".css",
  ".scss",
  ".html",
  ".json",
  ".yml",
  ".yaml",
  ".md",
  ".xml",
  ".cs",
  ".csproj",
  ".sln",
  ".slnx",
  ".sql",
  ".ps1",
  ".sh",
  ".bat",
  ".cmd",
]);

const MOJIBAKE_MARKERS = [
  "\u00C3\u0192",
  "\u00C3\u201A",
  "\u00C3\u0090",
  "\u00C3\u2020",
  "\u00C3\u201E",
  "\u00C3\u00A1\u00C2\u00BA",
  "\u00C3\u00A1\u00C2\u00BB",
  "\u00C3\u00A1\u00C2\u00BC",
  "\u00C3\u00A1\u00C2\u00BD",
  "\u00C3\u00A1\u00C2\u00BE",
  "\u00C3\u00A1\u00C2\u00BF",
  "\u00C3\u00A2\u00E2\u201A\u00AC",
  "\uFFFD",
];

function getStagedFiles() {
  const raw = execSync("git diff --cached --name-only --diff-filter=ACM", {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
  }).trim();

  if (!raw) return [];
  return raw.split(/\r?\n/).filter(Boolean);
}

function isTextFile(path) {
  const idx = path.lastIndexOf(".");
  if (idx < 0) return false;
  return TEXT_EXTENSIONS.has(path.slice(idx).toLowerCase());
}

function getStagedPatch(path) {
  return execSync(`git diff --cached --unified=0 --no-color -- ${JSON.stringify(path)}`, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
    maxBuffer: 10 * 1024 * 1024,
  });
}

function hasMojibakeAddedLines(patch) {
  const lines = patch.split(/\r?\n/);
  for (const line of lines) {
    if (!line.startsWith("+") || line.startsWith("+++")) continue;
    if (MOJIBAKE_MARKERS.some((marker) => line.includes(marker))) {
      return true;
    }
  }
  return false;
}

function main() {
  if (process.env.SKIP_MOJIBAKE_CHECK === "1") {
    process.exit(0);
  }

  const staged = getStagedFiles();
  const candidates = staged.filter(isTextFile);

  const badFiles = [];
  for (const file of candidates) {
    let patch = "";
    try {
      patch = getStagedPatch(file);
    } catch {
      continue;
    }
    if (hasMojibakeAddedLines(patch)) {
      badFiles.push(file);
    }
  }

  if (badFiles.length > 0) {
    console.error("Detected possible mojibake/encoding corruption in staged files:");
    for (const file of badFiles) {
      console.error(`- ${file}`);
    }
    process.exit(1);
  }
}

main();
