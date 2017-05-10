---
layout: post
title: F# will solve your everyday problem without a headache
date: '2016-02-12 23:43:57'
tags:
- functional
- fsharp
---

The last couple of days I had two experiences that triggered this post. The first one was a question at work regarding how to model a finite state machine (FSM) in java or a language similar to java. The second one was a [tweet](https://twitter.com/isaac_abraham/status/698178752560418816) by [Isaac Abraham](https://twitter.com/isaac_abraham): 

<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">It&#39;s really disappointing in 2016 to read this &quot;F# tends to be used in domains that have a lot of scientific or financial computations.&quot;</p>&mdash; Isaac Abraham (@isaac_abraham) <a href="https://twitter.com/isaac_abraham/status/698178752560418816">February 12, 2016</a></blockquote>
<script async src="//platform.twitter.com/widgets.js" charset="utf-8"></script>

The quote was from an [interview](http://ericlippert.com/2016/01/14/functional-style-follow-up/) with Eric Lippert, a former member of the C# team.

So how are these two events related? They are related because the greater community still has a misconception of functional languages, and to find better solutions to problems we need to look further than C# and java. I agree with Isaac 100 % that it is disappointing, and sad, to read and hear this misconception of F# and functional programming. At the same time I do see a change happening for everyone, with a more functional approach in the front-end with libraries like react and more functional aspects in languages like java and C#. What I don't understand is why doesn't people try harder to stay ahead of the game and learn functional programming, even though they do like it when it gets accepted by the greater community. Almost every C# developer I know is now comfortable, and like, lambda expressions. More and more developers discover that immutability isn't that bad after all, but still don't use it as much since it is so easy to use mutability in the languages we use the most. Everyone is now comfortable with the `var` keyword in C#, but still want to write the whole type on the other side of the equal sign.

F#, and probably scala and other functional languages that are strongly type handle all this in better ways than we are used to in java and C#. So my answer to the question was how I should define an FSM in F#. This is a problem that is not scientific and it has nothing to do with finance. This is a real problem that anyone of us could have in almost any type of application where you need to model a FSM, and model a FSM is probably something we should do more often. Before we get to my answer to the question, let's look at one of the references in one of the other answers. One colleague linked to [Martin Fowler's](https://twitter.com/martinfowler) book Domain-Specific Languages which you can read an example dealing with FSM here: http://www.informit.com/articles/article.aspx?p=1592379. Reading through the example gave me a headache and almost made my eyes bleed. The java code in the example is probably good java code, and I would probably think that all those fancy patterns where nice a couple of years back when the book was written. What is the problem with the code in the book? As I said, it is probably nice java code, but that doesn't mean it is readable. I took me a while to understand how the State Machine Model worked as described here: http://www.informit.com/articles/article.aspx?p=1592379&seqNum=2. Why was it so? The main reason it was hard for me to understand the sample code there was probably all the noise and extra syntax. The noise and extra syntax distracted me from the actual model which can never be a good thing. Let's go to the actual FSM defined on page 3: http://www.informit.com/articles/article.aspx?p=1592379&seqNum=3. The code here is definitely easier to understand since the level of noise has decreased due to the previous model, but it is still much noise. In fact, Fowler agree since he also provides multiple way to specify the model in other format than java code. There is one xml version, and two other versions. The only reason you need this is due to the fact that java is to verbose and the level of noise is too high.

## F# to the rescue

The whole purpose of this post is to show you that it is possible to solve real world problem with F# in more elegant ways than you would in languages like C# and java. I'll show you my code and then explain why I think it is better. I don't say that Fowler's java code is bad, more that the language might not be right tool.

<script src="https://gist.github.com/mastoj/9dfc21848c449fadcc93.js"></script>

Let me walk you through this piece of code. First I define a FSM module where all the general logic for implementing the FSM is defined. I define one type that represents the FSM. In the module I have also defined a set of helper functions that helps me create a FSM and also one [function](https://gist.github.com/mastoj/9dfc21848c449fadcc93#file-fowler_fsm-fsx-L12) that handles an event given an event and a FSM as input. All the helper functions takes a FSM as the last argument, since that allows me to pipe that argument in making it possible to have a really nice DSL in the language. The helper functions takes the provided FSM and returns a new FSM based on the provided FSM and the extra input. [`registerTransition`](https://gist.github.com/mastoj/9dfc21848c449fadcc93#file-fowler_fsm-fsx-L40) takes mapping from one state to another given an event. 

I defined all the valid events, commands and states on these lines: https://gist.github.com/mastoj/9dfc21848c449fadcc93#file-fowler_fsm-fsx-L54-L72. I really don't think they need any explanation. 

The creation of the FSM is done here: https://gist.github.com/mastoj/9dfc21848c449fadcc93#file-fowler_fsm-fsx-L75-L88. As you can see I'm using the helper functions which defines a really nice DSL for me. 

When I have the FSM defined I can actually try that it works and that is done here: https://gist.github.com/mastoj/9dfc21848c449fadcc93#file-fowler_fsm-fsx-L90-L105. First I define a helper infix operator that print the current state before calling the next function in each step. 

As I hope you can see in this example there are many advantages compared to the java version:

* No nulls
* Impossible to represent bad states
* The ration of noise vs. relevant code is significantly lower
* Shorter code, so it is easier to get the full picture
* No need for an external dsl

## Wrap up

F# (and most likely Scala) is a language you can, and should use, to solve your everyday problem. It will give you more concise code, easier to read code, and easier to maintain code. This will also have the positive side effect of less bugs. I understand that it might be a little bit weird at start since it is a whole new paradigm, but the reward is high on the other side. Learning to program functionally will help you write better programs in any language and you will also be ahead of the game when the functional features comes to java and C#. Note that even though those languages get some functional features they will never be as functional as F# and Scala since the base design of the languages are different. Do your self a favor and learn you some FP :)