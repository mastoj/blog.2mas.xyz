---
layout: post
title: Git Cheat Sheet
date: '2013-04-23 07:35:00'
tags:
- git
---

## Branching
 * Create branch

    `git checkout -b <branch name>`

 * Delete local branch

    `git branch -d <branch name>`

 * Delete remote branch

    `git push origin :<branch name>`

 * Synchronize your local branches with remote branches

    `git fetch -p|--prune`

## Getting changes
 * Simple get changes from remote

    `git fetch <remote name>`

 * Simple get changes and merge into your repository

    `git pull [<remote name> [<remote branch>]]`

## Working with remotes
 * Add remote

    `git remote add <name> <url>`

 * Delete remote

    `git remote rm <name>`

 * Rename

    `git remote rename <old name> <new name>`

 * Listing of remote branches

    `git remote show <remote name>`

 * Checkout and track remote branch

    `git checkout --track <remote name>/<remote branch>`

## Pushing the code
 * Initial push (to set the uplink)

    `git push -u <remote> branch`

 * If you have the upling set

    `git push` 

## Showing the changes
 * Show changes

    `git log`

 * Show changes with graph

    `git log --graph`

 * Show changes for all branches (with graph)

    `git log --graph --all`

 * Show one line per commit

    `git log --pretty=oneline`

   `pretty` has the following options: oneline, short, medium, full, fuller, email, raw. 

## Handling merge conflicts
 * Take source file

    `git checkout --theirs <file>`

 * Keep your file

    `git checkout --ours <file>`

## Diffing between branches
 * Regular diff with master

    `git diff master..<branch name>`

 * Get the name of the files that has changed

    `git diff --name-status master..<branch name>`

## Tagging
 * Creating an annotated tag (recommended)

    `git tag -a <tag name> -m '<message>'`

 * Creating an annotated tag from a checkin

    `git tag -a <tag name> -m '<message>' <commit checksum>`

 * Listing all tags

    `git tag`

 * Listing a defined list of tag

    `git tag -l 'v1.4.*'`

 * Show tag info

    `git show <tag name>`

 * Checkout from tag

    `git checkout <tag name>`

 * Sharing one tag

    `git push origin <tag name>`

 * Sharing all new tags

    `git push origin --tags`


## Sources
 * [Github workflow for submitting pull requests](https://www.openshift.com/wiki/github-workflow-for-submitting-pull-requests)
 * [Git rebasing](http://git-scm.com/book/en/Git-Branching-Rebasing)
 * [Another blog with tips](http://mislav.uniqpath.com/2010/07/git-tips/)