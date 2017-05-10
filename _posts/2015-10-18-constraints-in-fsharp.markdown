---
layout: post
title: Constraints in FSharp
date: '2015-10-18 06:57:19'
tags:
- net
- fsharp
---

Constraints in F# is a really powerful feature, but it is also an area that I think is missing some clear documentation about how to use it. This will be a short write up for future me when I need it, and I also it will help some lost souls out there. 

## Inline functions

F# will most of the time handle the types for you, also generics when it can do so. A simple example:

```language-fsharp    
let add x y = x + y
```
This will when it stands all by itself resolve to the have the type: 

   val add : x:int -> y:int -> int

That all the compiler can do when it is not provided any more information. If you instead write

```language-fsharp
let add x y = x + y
add "Hello " "world"
```

`add` will get the type

```language-fsharp
val add : x:string -> y:string -> string
```
since you on line two specify that you will use it with strings. What should you do if you want to add it with any arguments that supports the `+` operator? This is where the `inline` keyword is useful (it can also be used in some optimization scenarios). So let us define the function again and adding the `inline` keyword:

```language-fsharp
let inline add x y = x + y
```

The type for this version of add is a little bit more complicated:

```language-fsharp
val inline add :
  x: ^a -> y: ^b ->  ^c
    when ( ^a or  ^b) : (static member ( + ) :  ^a *  ^b ->  ^c)
```

What does this mean? 

* `x: ^a -> y: ^b ->  ^c` specify that the function takes two arguments and returns something. The argument has types `^a` and `^b` and the result have type `^c`
* `when ( ^a or  ^b) : (static member ( + ) :  ^a *  ^b ->  ^c)` is adding a constraint on the types `^a`, `^b` and `^c`. The constraint says that it must exist a static operator `+` that takes a tuple of type `^a *  ^b` and returns type `^c`.

We got all that by adding the keyword `inline`, but why? When adding the `inline` keyword you basically say that whenever you see the function name somewhere in the code replace that name with this function definition. This basically gives you one new implementation of the function every time you use it. If you don't use `inline` you will only have one implementation and that is why you don't get it as generic as you would like. This is my simplified explanation, someone on the compiler team can probably explain it a lot more in detail.


## Constraining on interfaces

I won't go on with you can constrain the types on interfaces because it quite simple and there is more interesting constraint in the next section. I'll just throw the code at you

```language-fsharp
type ISimpleInterface =
    abstract member Add: int -> int -> int

type SimpleClassA() =
    interface ISimpleInterface with
        member this.Add x y = x + y

type SimpleClassB() =
    interface ISimpleInterface with
        member this.Add x y = x + y

let doSimple<'T when 'T :> ISimpleInterface>  (x: 'T) = x.Add 5 5
let doSimple2 (x: ISimpleInterface) = x.Add 5 5

let a = new SimpleClassA()
let b = new SimpleClassB()
doSimple a
doSimple b
```

Here first define an interface and then two implementations of that interface. After that I defines two functions `doSimple` and `doSimple2`, which are basically identical. I prefer to use the second variant when possible. 

## If it walks like a duck and quack like a duck

[Duck typing](https://en.wikipedia.org/wiki/Duck_typing) can be achieved in F# by using constraint. This is really powerful since you can write functions that take in arguments and as long as the types of the arguments support doesn't violate the constraint you can use them without the need of an interface. Why would you want to do this you might ask? The reason I started to look into this was actually because I needed it at work. I am using the excellent [`SqlClient` type provider](http://fsprojects.github.io/FSharp.Data.SqlClient/), and wanted to use the `SqlProgrammabilityProvider` to read and update multiple tables using the pipe operator. The problem is that the `Update` method is defined on the generated table type and not as a static `Update` method. A simplified version of what I had looks somewhat like this:

```language-fsharp
type SomeClass1() =
    member
       this.Add(a:int, b:int) = a + b

type SomeClass2() =
    member
        this.Add(a:int, b:int) = a + b
```

and I wanted an `add` method that could be applied to either `SomeClass1` or `SomeClass2` so I could write `someClassInstance |> add 2 3`. To achieve that you use member constraints like this:

```language-fsharp
type SomeClass1() =
    member
        this.Add(a:int, b:int) = a + b

type SomeClass2() =
    member
        this.Add(a:int, b:int) = a + b

let inline add (y:int) (z:int) (x: ^T when ^T : (member Add : int*int->int)) =
    (^T : (member Add : int*int->int) (x,y,z))

SomeClass1() |> add 2 3
SomeClass2() |> add 2 3
```

In the function definition I define that I the argument `x` must have an operator called `Add` that has type `int*int->int`. In the body of the function I specify that I will call the `Add` function on variable `x` with the input of `y` and `z`. It looks complicated until you get your head around it. It is also important to specify the method as `inline`. One thing that is good to know is that it doesn't work for curried functions even though you don't get a compile time error. 

The case I had was a slightly more complicated since the `Update` method I wanted to use had some optional arguments. If you know that optional arguments is represented as `Option` types in F# the code isn't that surprising:

```language-fsharp
type SomeClass1Option() =
    member
        this.Add(?a:int, ?b:int) = 
            match a,b with
            | Some x, Some y -> x + y
            | _, _ -> 0

type SomeClass2Option() =
    member
        this.Add(?a:int, ?b:int) = 
            match a,b with
            | Some x, Some y -> x + y
            | _, _ -> 0

let inline add (y:int) (z:int) (x: ^T when ^T : (member Add : int option*int option->int)) =
    (^T : (member Add : int option*int option->int) (x,Some y,Some z))

SomeClass1Option() |> add 2 3
SomeClass2Option() |> add 2 3
```

Given all this I ended up writing an `update` method that looks like this:

```language-fsharp
let inline updateTable(table: ^T when ^T : (member Update : SqlConnection option*SqlTransaction option*int option -> int)) = 
    (^T : (member Update : SqlConnection option*SqlTransaction option*int option-> int) (table, None, None, None))
```

That method can be used with piping to call the `Update` method on any table generated with `SqlProgrammabilityProvider` with default arguments.

You can also write constraint on static operators, but I won't cover that. The documentation for constraints is found here: https://msdn.microsoft.com/en-us/library/dd233203.aspx. 
 

