
1. Review uncommited git changes
2. Review what was changes
3. Propose cmmmit message following conventional commits and conventional commits guidelines
4. Wait for my review and approval!
5. After my review commit the changes
6. propose nam of the git branch and ask if I want to push changes. Wait for my review and approval!
7. After my review push to the new branch. Do not create new branch locally. Example command `git push origin HEAD:type/very-short-description`


Guidelines for version control using conventional commits:
<conventional_commits>
- Follow the format: `type(scope): description` for all commit messages
- Use consistent types (feat, fix, docs, style, refactor, test, chore) across the project
- Include issue references in commit messages to link changes to requirements
- Use breaking change footer (`!` or `BREAKING CHANGE:`) to clearly mark incompatible changes
</conventional_commits>