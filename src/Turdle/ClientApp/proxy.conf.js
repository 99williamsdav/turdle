const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:13646';

const PROXY_CONFIG = [
  {
    context: [
      "/weatherforecast",
      "/getgamestate",
      "/getpreviousalias",
      "/getpointschedule",
      "/getfakereadyboard",
      "/gameHub",
      "/adminHub",
      "/homeHub",
      "/img"
   ],
    target: target,
    secure: false
  }
]

module.exports = PROXY_CONFIG;
