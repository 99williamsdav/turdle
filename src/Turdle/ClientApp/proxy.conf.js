const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:13646';

const PROXY_CONFIG = [
  {
    context: [
      "/weatherforecast",
      "/getgamestate",
      "/getrooms",
      "/getpreviousalias",
      "/getpointschedule",
      "/getfakereadyboard",
      "/img"
   ],
    target: target,
    secure: false
  },
  {
    context: [
      "/gameHub",
      "/adminHub",
      "/homeHub"
    ],
    target: target,
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
