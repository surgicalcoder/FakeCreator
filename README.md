# FakeCreator
C# framework for creating strongly-typed fakes and mapping between layers.

## Usage

There are two steps, the first is to generate a .json mapping of the class you want:

~~~~ 
FakeCreator2 -Te "(Path to templates folder)" -M "main.json" -D "(Path to single or multiple dll's seperated with ; )" -C "{Prefix, if requred}" -R "{Source Classes to generate, seperated with a ,}" -G "true"
~~~~

Once this has generated and you are happy with it, change the -G statement to false, and you will get a bunch of outputs in the same directory as your json file:

~~~~ 
FakeCreator2 -Te "(Path to templates folder)" -M "main.json" -D "(Path to single or multiple dll's seperated with ; )" -C "{Prefix, if requred}" -R "{Source Classes to generate, seperated with a ,}" -G "false"
~~~~
