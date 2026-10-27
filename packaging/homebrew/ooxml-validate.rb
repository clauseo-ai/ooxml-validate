# Homebrew formula for the ooxml-validate command-line tool. This is a template, not a formula you can install as it stands.
#
# The release workflow (.github/workflows/release.yml) fills the @VERSION@ and @SHA256_*@ placeholders with
# packaging/homebrew/fill-formula.sh, from the release's `.sha256` files, and attaches the result to the draft release
# as ooxml-validate.rb. To publish it, a maintainer copies that file into a tap repository, as
# Formula/ooxml-validate.rb in a repository named homebrew-tap, so that `brew install clauseo-ai/tap/ooxml-validate`
# works. Nothing here pushes to a tap. See https://docs.brew.sh/Taps and https://docs.brew.sh/Formula-Cookbook.
#
# The archives hold a single directory, ooxml-validate-<rid>/, with the self-contained binary, LICENSE, NOTICE and
# README.md in it. Homebrew enters that directory when it unpacks an archive, so the glob below finds the binary either way.
class OoxmlValidate < Formula
  desc "Validate .docx files with the Open XML SDK validator, with a count-based compare for edits"
  homepage "https://github.com/clauseo-ai/ooxml-validate"
  version "@VERSION@"
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
  end

  test do
    assert_match "ooxml-validate #{version}", shell_output("#{bin}/ooxml-validate --version")
    # A file that is not a package is an error: exit 1, not a crash and not a usage error (2).
    (testpath/"not-a-package.docx").write("this is not a zip")
    shell_output("#{bin}/ooxml-validate not-a-package.docx", 1)
  end
end
