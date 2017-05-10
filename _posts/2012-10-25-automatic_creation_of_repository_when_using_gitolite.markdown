---
layout: post
title: Automatic creation of repository when using gitolite
date: '2012-10-25 07:26:00'
tags:
- git
- gitolite
- bash
---

The last couple of days I've been playing around with git and setup my own git server using first gitosis and then gitolite. When playing around I created a lot of different test repositories to make sure everything work. To stop repeating myself I created three minor bash functions to make it easier to add repositories in gitocline. The script I ended up with are the following:

    ar() {
        if [ "$1" == "" ] ; then echo "[E] One arg is needed!"; return 1; 
	else
            cd ~/<some path to your>/gitolite-admin/conf
            echo -e "" >> gitolite.conf
            echo -e "repo ${1}" >> gitolite.conf
            echo -e "RW+ = <your gitolite username>" >> gitolite.conf
            cd ..
            git commit -am "Added repo ${1}"
            git push
        fi 	
    }

    sr() {
        if [ "$1" == "" ] ; then echo "[E] One arg is needed!"; return 1; 
        else
            mkdir <path to where you have your repositories>/${1}
            cd <path to where you have your repositories>/${1}
            git init
            git remote add origin gitolite@e2n.no:${1}
            touch README
            git add .
            git commit -am "Initial commit"
            git push origin master
        fi
    }

    cr() {
        if [ "$1" == "" ] ; then echo "[E] One arg is needed!"; return 1; 
        else
            ar ${1}
            sr ${1}
        fi
    }
		
It is somewhat static code, but I'm not that likely to change where I have the folders on my local computer anyway. I should probably mention that these three functions are stored in the .bashrc file and that I assumes you have checked out the gitolite-admin repository. Let us walk through the three functions.

 - ar (short for add repository): will navigate to the configuration folder for gitolite and add the repository plus read and write access for a user name (should probably use a group instead). It will also commit the changes and push them to the server to create the repository.
 - sr (short for set up repository): will first create a folder for the repository, then initialize git in that folder, add a remote to your new repository and make an initial commit and push.
 - cr (short for create repository): will execute both the above functions.

This will allow us to create a repository and set it up on our local machine by running

    $ cr repository_name

That's all.