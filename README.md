# MarvelApi
## Summary
This project can be found at: https://github.com/bodzilla/MarvelApi

This is a .NET console application which has the following functionalities:
- Get the ids of the top 10 Marvel characters using Marvel's API. A top character is one with
the most number of appearances on comics and stories.
- Get details of a single Marvel character's powers scraped from Marvel's Wiki, this also includes general character details using Marvel's API.
- Get a translation of the single Marvel character's details using Google's Translate API.
- All objects return some analytics regarding the requests made.
- Able to use gzip compression to improve performance.
- Able to cache results to improve performance.
- Warnings and errors are logged to a text file with full stack traces.

## Setting up dependencies
### Marvel API
In order to use Marvel's API, you will need to create a developer account, which will generate your API keys:
1) Sign up here: https://developer.marvel.com
2) After activating your account, you can get your API keys from: https://developer.marvel.com/account
3) Retrieve your public and private keys and store them in a safe location. Do not share them with unauthorised users.
4) Your account will be limited to 3000 calls a day - keep this in mind should you notice regular API fails.

### Google Translate API
In order to use Google's Translate API, you will need to sign up and activate a project, which will generate a JSON containing some credentails, including your API keys. This API is not free but new accounts can be activated to be credited with 200GBP cost-free:
1) Sign up for a Google account here: https://accounts.google.com/signup/v2
2) In order to activate your 200GBP free credit, you will need to add a billing account here: https://console.cloud.google.com/billing
3) After activating your billing account, you can now create a project to use Google's API here: https://cloud.google.com/translate/docs/quickstart
4) Click on `Set up a project` and give your project an appropriate name.
5) Click on `Download private key as JSON` and store in a safe location. Do not share this with unauthorised users.

## Building and configuration
Now that you have all the access points in place, you can now build the application and configure it:
1) In Visual Studio, navigate to `MarvelApi` and load `MarvelApi.sln`.
2) Right click on the solution and build the solution in `Release` mode. (Note: If the build fails, try again after a few seconds as it may take a little while to download all the relevant packages).
3) You can now close down Visual Studio.
4) Navigate to `MarvelApi\MarvelApi\bin\Release`.
5) Open the configuration file `MarvelApi.exe.config`
6) For the `Security` configurations - fill these in appropriately.
7) Optionally, you can modify the `Web/API` configurations.

## Running the application
Finally, you can run the application via a shell console. To run, simply run the program `.\MonzoApi.exe` followed by any of the below arguments in lower case:

Argument | Output 
--- | --- 
`marvel characters` | Returns the ids of the top 10 Marvel characters using Marvel's API as well as total characters in the Marvel universe and total characters in comics and stories. A top character is one with the most number of appearances on comics and stories.
`marvel characters {characterId}` | Returns general details about a single Marvel character using Marvel's API given their character ID.
`marvel powers {characterId}` | Returns details of a single Marvel character's powers scraped from Marvel's Wiki, this also includes general character details using Marvel's API given the character ID.
`marvel powers {characterId} {languageCode}` | Returns a translated version of the single Marvel character's details using Google's Translate API given the character ID. The language code is in ISO-639-1 format. A full list can be found at: https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes

## Limitations
- Marvel's API can only be requested 3000 times a day due to limitations on free developer accounts.
- Be aware that every Google Translate API call will use your free credit - keep an eye on this.
- Getting single character responses do not work for characters classed as teams, such as the X-Men.
- Some text may not translate to desired language due to limitations on Google's API to interpret certain words if they are connected, such as "Spider-Sense".
- Error logs do not include invalid arguments as these are displayed on the console directly.
