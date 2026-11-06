# Making the repo public safely

Steps for opening the Decay repository while keeping control of what gets
merged and keeping secrets out. Most of this is GitHub settings, done once in
the browser. Nothing here changes the app.

*Written for repo `ToothbrushStudio/Decay`. Menu names move around on GitHub;
if one doesn't match, search the repo settings page for the feature name.*

## The two things that can hurt you

1. **The license-signing private key leaks.** Anyone holding it can mint
   unlimited Pro keys. The public key is baked into every shipped build, so you
   can't rotate it without invalidating every key you've sold. Treat it as
   permanent and keep it out of git, CI, chat and cloud sync.
2. **Someone merges a malicious change** that ends up in a signed build, for
   example code that reads the passphrase or weakens the encryption. Branch
   protection and code review stop this.

Everything below serves one of those two.

## Before you flip to public

### 1. Check the history for secrets

Making a repo public publishes its **entire history**, including files you
deleted later. Scan it first.

- **Done on 2026-11-05:** gitleaks 8.16 over every branch found no secrets.
  It flagged two strings that aren't secrets (Microsoft's public
  `PublicKeyToken` in the retired WinForms prototype, and Rot's public mask
  label); they are listed in `.gitleaksignore`. No license key in the history
  validates against the shipped public key. Run the scan again just before
  going public, since new commits land in between.
- Quick manual check:

  ```bash
  git log --all --name-only --pretty=format: | sort -u | grep -iE "\.pem|\.pfx|\.p12|\.snk|keys?\.txt|secret|\.env|private"
  git log --all -p -S"BEGIN PRIVATE KEY" --oneline
  git log --all -p -S"BEGIN EC PRIVATE KEY" --oneline
  ```

- Better: run a real scanner over every branch, once, before going public:

  ```bash
  gitleaks detect --source . --log-opts="--all"
  # or: trufflehog git file://. --since-commit <first commit>
  ```

- Also look at **other branches and open pull requests**. A fork or PR from a
  branch you forgot about is part of what becomes visible.
- **If anything sensitive turns up**, don't just delete it in a new commit.
  Rewrite history (for example with `git filter-repo`), force-push, and assume
  the secret is compromised. For the license-signing key that would mean
  generating a new pair and re-issuing keys, so check first.

### 2. Keep secret-looking files out of git going forward

**Done:** `.gitignore` now covers `*.pem`, `*.pfx`, `*.p12`, `*.snk`, `*.key`,
`*.env`, `keys.txt`, `*-keys.txt` (so `itch-keys.txt`) and
`decay-publisher-private*`.

Keep the private key and the key pool (`itch-keys.txt`) outside the repo
folder entirely, for example in a password manager or an encrypted drive.
`dotnet run batch ... > itch-keys.txt` writes into the current folder, so run it
from somewhere that isn't the repo.

### 3. Choose a license

A public repo with no license is still legally "all rights reserved": people can
read it but have no permission to use it. Decide on purpose:

| Goal | Typical choice |
|---|---|
| Anyone may use and sell it, no strings | MIT or Apache-2.0 |
| Others can read it and build it for themselves, but can't sell competing copies | A source-available license (for example PolyForm Noncommercial or BSL) |
| Anything built on it must stay open | GPL-3.0 or AGPL-3.0 |

**Decided:** all rights reserved (`LICENSE`), which can be relaxed later.

Add the chosen text as a `LICENSE` file in the repo root. The Pro keys are
checked offline, so anyone with the source could remove the check from their
own build. An open license doesn't change that; it's a fact of shipping the
source. A license decides what others may legally do with the code, and
selling Pro still depends on people choosing to pay.

If you want to keep the right to change the license later, consider asking
contributors to agree to that before you accept outside code.

### 4. Add a security policy and a way to report problems

- Create `SECURITY.md` in the repo root. It should say how to report a
  vulnerability (the contact email, or GitHub's private reporting) and what you
  will do. A short, honest policy is fine.
- In **Settings → Code security**, enable **Private vulnerability reporting**,
  so people can report privately instead of in a public issue.

## GitHub settings to turn on

### 5. Protect `main`

In **Settings → Rules → Rulesets → New branch ruleset** (or **Settings →
Branches → Add branch protection rule**), target `main` and enable:

- **Require a pull request before merging.** No direct pushes to `main`.
- **Require approvals:** 0 while you are the only maintainer. GitHub doesn't
  let you approve your own pull request, so requiring 1 approval (or code
  owner review) would block every merge. With a second maintainer, raise it
  to 1 and tick **Require review from Code Owners**; `CODEOWNERS` is ready.
- **Dismiss stale approvals when new commits are pushed** (matters once
  approvals are required).
- **Block force pushes** and **restrict deletions.**
- **Require status checks to pass** once you have a CI workflow (step 8), and
  select the test job.
- On the free plan, rulesets and the status-check list only work on a
  **public** repo. A private repo shows "Your rulesets won't be enforced" and
  stays open to direct pushes until it is public or on a paid plan.
- **Do not allow bypass**, or limit bypass to yourself. If you are the only
  maintainer and want to be able to merge your own PRs, you can allow your own
  account to bypass. That's a trade-off between convenience and safety; solo
  maintainers often do this.

### 6. Add a `CODEOWNERS` file

Create `.github/CODEOWNERS` so your account has to approve changes. Replace the
handle with yours:

```
# Everything needs the maintainer's review
*                       @YOUR-GITHUB-HANDLE

# Extra attention on the parts that must never silently change
/Decay.Core/            @YOUR-GITHUB-HANDLE
/Decay.Licensing/       @YOUR-GITHUB-HANDLE
/Decay.App/PublisherKey.cs  @YOUR-GITHUB-HANDLE
/docs/payload-format-v1.md  @YOUR-GITHUB-HANDLE
/.github/               @YOUR-GITHUB-HANDLE
```

The `Decay.Core`, `Decay.Licensing` and `PublisherKey.cs` paths matter most:
that is the encryption format, license checking and the public key. A change in
any of them can break every shipped image or let people bypass licensing.

### 7. Control who can write

- Outside contributors work by **forking** and opening pull requests. They can't
  push to your repo unless you add them as collaborators.
- Keep **Collaborators and teams** short. Give people the lowest role that does
  the job (Read or Triage, rarely Write).
- Turn on **two-factor authentication** for your account, and require it for the
  organization if you add members (**Organization → Settings → Authentication
  security**).
- Under **Settings → General → Pull Requests**, consider disabling merge types
  you don't use, and enabling **Automatically delete head branches**.

### 8. Make CI safe for outside pull requests

If you add a GitHub Actions workflow to run the tests, set it up carefully,
because outside pull requests can try to run code in it.

- **Settings → Actions → General → Fork pull request workflows:** require
  approval for **all outside collaborators** before workflows run.
- Set **Workflow permissions** to **Read repository contents** only.
- Use the `pull_request` trigger for tests. Avoid `pull_request_target` unless
  you really understand it, because it runs with repo secrets.
- **Never put the license-signing key, the code-signing certificate or itch
  credentials in repository secrets** for a public repo's CI. Build and sign
  releases by hand on your own machine.
- Pin third-party actions to a full commit SHA, not a tag.

**Done:** `.github/workflows/test.yml` runs on every push and pull request
with read-only permissions and actions pinned to commit SHAs. A Linux job
runs the three test projects (including the golden vectors) and the console
license cross-checks. A Windows job builds the console edition and runs
`decay selftest` on the real .NET Framework 4.8, plus round trips with the
.NET 10 engine. The MAUI app isn't built in CI (it needs the MAUI workload).

### 9. Turn on automatic scanning

Under **Settings → Code security** (free on public repos):

- **Secret scanning** and **Push protection**, which block a push containing a
  recognized key or token.
- **Dependabot alerts** and **Dependabot security updates.** The engine has no
  dependencies, but the app does.
- **Code scanning (CodeQL)**, if you want automated checks for common bug types.

## Ongoing habits

- **Read every pull request as if it were hostile**, especially anything that
  touches crypto, licensing, the payload format or the build files
  (`*.csproj`, workflows). Small, plain-looking changes are where problems hide.
- **Never merge a change that alters the frozen format.** The
  `FrozenFormatTests` golden vector should fail and block it. If it fails,
  revert the change, not the test.
- **Build releases from a clean checkout of a commit you reviewed**, on your own
  machine, then sign and publish. Don't ship binaries built by anyone else.
- **Keep the signing private key off every shared machine.** If you ever suspect
  it leaked, stop selling keys and decide on a plan before doing anything else.
- **Say in the README that only you merge**, and that bug reports and ideas are
  welcome as issues. It sets expectations and cuts down unwanted pull requests.

## Checklist

- [x] History scanned with a real tool, no secrets found (re-run before going public)
- [x] `.gitignore` extended
- [x] Key pool and private key stored outside the repo folder
- [x] License chosen and `LICENSE` added
- [x] `SECURITY.md` added
- [x] Private vulnerability reporting on
- [x] Ruleset on `main`: PR required, both `test` jobs required, no force
      pushes or deletions (0 approvals while solo)
- [x] `.github/CODEOWNERS` added with your handle
- [x] 2FA on for your account
- [x] CI workflow added with read-only permissions
- [x] Fork-PR workflow approval required
- [x] No signing keys or certificates in repo secrets
- [x] Secret scanning, push protection and Dependabot on
- [x] Then switch the repo to **public** (**Settings → General → Danger Zone →
      Change visibility**)
