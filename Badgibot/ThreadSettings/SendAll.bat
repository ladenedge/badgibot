
for %%F in (*.json) do (
   curl -X POST -H "Content-Type: application/json" --data @"%%~dpnxF" "https://graph.facebook.com/v2.8/1728563464137725/thread_settings?access_token=EAAYkHotAaZC0BAFOKOjz5htHSWitdzqJZCfj6XRZBNJBUJQCLamDs2p8V8ZABPtybH7PZAPYIo0P1I8iAh8gdEjqg5Y9x4gPfsEwwQZBLh2yvUZCiEND8zkrRcHiRZCGmZCBdHlgWdq7csfyw5XEzNqdfetIOdDYzcVK6ywqfXsn0cwZDZD"
)
