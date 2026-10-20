# Security policy

## Supported versions

IDE-tabs is in early alpha. Only the latest release receives fixes.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Report it privately through
GitHub: on the repository's **Security** tab, choose **Report a vulnerability**.

Include what you found, the steps to reproduce it, and the IDE-tabs and Windows versions.
You can expect a first answer within a week. Once a fix is released, the report can be
made public, with credit to you if you wish.

## Scope

IDE-tabs runs with your normal user rights, reads window titles and positions, and moves
and groups windows. Of particular interest are ways for another program or a crafted
window title to make IDE-tabs run code, change files, or act on windows it should leave
alone.

## Downloads

Releases are built from this repository by GitHub Actions. They are not code-signed yet;
each release lists the SHA-256 of its downloads in `SHA256SUMS.txt`.
