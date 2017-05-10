---
layout: post
title: Refactor your code - remove those if-else if-else and switch statements
date: '2012-10-25 07:19:00'
tags:
- net
- functional
- refactoring
---

I'm currently working with some legacy code and came across a really long if-else if-else function. We've all been down that path writing that type of code. A situation where this is pretty common in .Net, and that is where I stumbled on it this time, is when acting on a some kind of command from a grid view for example. The code that I refactored looked something like this:

    public void gridView_OnRowCommand(object sender, GridViewCommandEventArgs e)
    {
	    string commandName = e.CommandName;
		if(commandName == "command1")
		{
			// A lot of code for command 1
		}
		else if(commandName == "command2")
		{
			// A lot of code for command2
		}
		else if(commandName == "command3")
		{
			// A lot of code for command3
		}
		else if(commandName == "command4")
		{
			// A lot of code for command4
		}
    }

Every where were it says "A lot of code for commandx" contained some kind of logic that was pretty hard to understand. So the first step in refactoring this piece of code is actually to learn what those commands are and create separate methods for each method like such: 

	public void Command1()
	{
		// A lot of code for command1
	}

	public void Command2()
	{
		// A lot of code for command2
	}
	public void Command3()
	{
		// A lot of code for command3
	}
	public void Command4()
	{
		// A lot of code for command4
	}

The function name must also have a descriptive name and not just `Commandx` as I have in the example here. Now one might think that we could just change our if-else if-else to a switch and call the commands from the switch. But I don't think that is enough. The event handler should just handle the event it should **NOT** be responsible for executing the right command, so what I ended up doing was a dictionary where I mapped the command names to their respective functions and created a function that was responsible for executing the right command given a command string. The final code looked something like: 

    private Dictionary<string, Action> _commandDictionary;
    public Dictionary<string, Action> CommandDictionary
    {
        get
        {
            if (_commandDictionary == null)
            {
                _commandDictionary = new Dictionary<string, Action>();
                _commandDictionary.Add("Command1", Command1);
                _commandDictionary.Add("Command2", Command2);
                _commandDictionary.Add("Command3", Command3);
                _commandDictionary.Add("Command4", Command4);
            }
            return _commandDictionary;
        }
    }

    public void gridView_OnRowCommand(object sender, GridViewCommandEventArgs e)
    {
        string commandToExecute = e.CommandName;
        ExecuteCommand(commandToExecute);
    }

    private void ExecuteCommand(string command)
    {
        if (CommandDictionary.ContainsKey(command))
        {
            CommandDictionary[command]();
        }
        else
        {
            throw new ArgumentException("Invalid command: {0}", command);
        }
    }
	
    public void Command1()
    {
        // A lot of code for command1
    }

    public void Command2()
    {
        // A lot of code for command2
    }

    public void Command3()
    {
        // A lot of code for command3
    }

    public void Command4()
    {
        // A lot of code for command4
    }

The resulting code is not necessarily shorter, but every piece of code has it's own "purpose". The event handler just extract the command that should be executed, the `ExecuteCommand` makes the check if the command is valid and calls the responding command and the command functions do the actual work that is requested. Some advantages you get with this solution are that it is much easier to test each function by itself than it is was before and it is much easier to follow what the code actually does.

You could take this one step further, or if you don't have something like `Action` that I use here in your language of choice, and create an interface called `ICommand` that defines an `Execute` method and wrap all the commands in separate classes that implements that interface. But for now this works fine for me since I only use it in one place and it is not meant to be used anywhere else.