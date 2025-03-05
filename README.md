# SVD-API
 Easy to use API lib for @Senior-S's steam voice api

# Usage
1. Create a SVDClientFactory
```
SVDClientFactory factory = new("UserName", "Api-Key");
```

2. Create a SVDClient
```
SVDClient client = await factory.CreateClientAsync();
```

# TODO
- Add audio capture helpers
- Add audio playback helpers
- Put on nuget
