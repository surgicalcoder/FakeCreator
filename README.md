# FakeCreator
C# framework for creating strongly-typed fakes/DTO and mapping between layers.

## Usage

There is one step, which is to generate a .json mapping of the class you want, and all the files you want:

~~~~ 
FakeCreator -Te "(Path to templates folder)" -M "main.json" -D "(Path to single or multiple dll's seperated with ; )" -C "{Prefix, if requred}" -R "{Source Classes to generate, seperated with a ,}" -G "true"
~~~~

There are more options, simply run FakeCreator.exe with no parameters to get usage.