---
layout: post
title: Linq tip of the day - Aggregate
date: '2012-10-25 07:28:00'
tags:
- net
- linq
---

One thing that I have noticed is that people tend to not be aware of the [`Aggregate`](http://msdn.microsoft.com/en-us/library/system.linq.enumerable.aggregate.aspx) function that exist for linq. If you learn how to use the `Aggregate` function it will be useful for you in many scenarios. One scenario where I have found it useful many times is in logging scenarios, especially when you want to log some output from a list or array as the example below:

    var list = new string[]{"These", "values", "need", "to", "be", "logged", "in", "reverse"};
    var output = list.Aggregate((aggregate, next) => next + " " + aggregate);
    Console.WriteLine(output); //prints "reverse in logged be to need values These"

Another useful scenario is when you want to do some mathematical calculations on a series of numbers with a starting condition, like a checking account in the example below:

    var currentBalance = 100000;
    var withdrawals = new int[] { 1000, 2034, 16243, 2423, 44234 };
    var updatedBalance = withdrawals.Aggregate(currentBalance, (aggregate, next) => currentBalance - next);
    Console.WriteLine(updatedBalance);

The following is a little bit more complicated but I think it really shows the strength of the `Aggregate` function. Let say you have some kind of rule engine that validates some kind of object. Now you what to run all these rules against your object, and at the same time update the object so it stores a collection of violated rules. I think that a realistict scenario and the following code show how you can solve the validation using `Aggregate`.

    class Program
    {
        static void Main(string[] args)
        {
            var rules = new List<Rule> { new RemoveARule(), new RemoveXRule(), new RemoveZRule() };
            var input = new MySuperSecretEntity();
            var output = rules.Aggregate(input, (aggregate, rule) => rule.Validate(aggregate));
            // input.ViolatedRules now contain a set of violated rules
            var violatedRules = input.ViolatedRules.Aggregate("", (aggregate, next) => aggregate + ", " + next.ToString());

            Console.WriteLine(violatedRules); // Prints the type of the two violated rules
            Console.ReadLine();
        }
    }

    class MySuperSecretEntity
    {
        public string DoILookFine = "This is the string that will be validated against a set of rules!!!";
        public List<Rule> ViolatedRules { get; set; }

        public MySuperSecretEntity()
        {
            ViolatedRules = new List<Rule>();
        }
    }

    abstract class Rule
    {
        protected string MustContainLetter { get; set; }

        public MySuperSecretEntity Validate(MySuperSecretEntity input)
        {
            var isValid = input.DoILookFine.Contains(MustContainLetter);
            if (!isValid) input.ViolatedRules.Add(this);
            return input;
        }
    }

    class RemoveARule : Rule
    {
        public RemoveARule()
        {
            MustContainLetter = "a";
        }
    }

    class RemoveXRule : Rule
    {
        public RemoveXRule()
        {
            MustContainLetter = "x";
        }
    }

    class RemoveZRule : Rule
    {
        public RemoveZRule()
        {
            MustContainLetter = "z";
        }
    }

That was my .Net/linq tip of the day.