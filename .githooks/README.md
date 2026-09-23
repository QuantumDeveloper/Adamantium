# Git hooks

`master` takes pull requests only. These two hooks say so before the server does: one refuses a commit made while
standing on `master`, the other refuses a push aimed at it - including `git push origin HEAD:master` from any branch.

Git does not use them until a clone is told where they live. Once per clone:

```sh
git config core.hooksPath .githooks
```

Both are advisory: `--no-verify` walks past either. The branch ruleset on GitHub is what actually enforces this.
