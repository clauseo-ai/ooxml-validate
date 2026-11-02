# Homebrew formula for the ooxml-validate command-line tool, for the tap clauseo-ai/homebrew-tap, so that
# `brew install clauseo-ai/tap/ooxml-validate` works. See https://docs.brew.sh/Taps.
#@ This is a template, not a formula you can install as it stands. The release workflow
#@ (.github/workflows/release.yml) fills in the version and the four SHA-256 values with
#@ packaging/homebrew/fill-formula.sh, from the release's `.sha256` files, and attaches the result to the draft
#@ release as ooxml-validate.rb. The fill script drops these "#@" lines. A maintainer copies the filled file into
#@ the tap as Formula/ooxml-validate.rb. Nothing here pushes to a tap.
#
# The archives hold a single directory, ooxml-validate-<rid>/, with the self-contained binary, LICENSE, NOTICE,
# THIRD-PARTY-NOTICES.txt and README.md in it. Homebrew enters that directory when it unpacks an archive, so the glob
# below finds the binary either way.
class OoxmlValidate < Formula
  desc "Validate .docx files with the Open XML SDK validator, or compare two"
  homepage "https://github.com/clauseo-ai/ooxml-validate"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/clauseo-ai/ooxml-validate/releases/download/v@VERSION@/ooxml-validate-osx-arm64.tar.gz"
      sha256 "@SHA256_OSX_ARM64@"
    end
    on_intel do
      url "https://github.com/clauseo-ai/ooxml-validate/releases/download/v@VERSION@/ooxml-validate-osx-x64.tar.gz"
      sha256 "@SHA256_OSX_X64@"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/clauseo-ai/ooxml-validate/releases/download/v@VERSION@/ooxml-validate-linux-arm64.tar.gz"
      sha256 "@SHA256_LINUX_ARM64@"
    end
    on_intel do
      url "https://github.com/clauseo-ai/ooxml-validate/releases/download/v@VERSION@/ooxml-validate-linux-x64.tar.gz"
      sha256 "@SHA256_LINUX_X64@"
    end
  end

  def install
    bin.install Dir["**/ooxml-validate"].first => "ooxml-validate"
    # Homebrew keeps LICENSE, NOTICE and README.md by itself, but not the .NET runtime's third-party notices.
    prefix.install Dir["**/THIRD-PARTY-NOTICES.txt"]
  end

  test do
    assert_match "ooxml-validate #{version}", shell_output("#{bin}/ooxml-validate --version")
    # A file that is not a package is an error: exit 1, not a crash and not a usage error (2).
    (testpath/"not-a-package.docx").write("this is not a zip")
    shell_output("#{bin}/ooxml-validate not-a-package.docx", 1)
  end
end
