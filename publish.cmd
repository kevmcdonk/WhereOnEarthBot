nuget restore
msbuild CoreBot.sln -p:DeployOnBuild=true -p:PublishProfile=dailybingchallengebot-Web-Deploy.pubxml -p:Password=0xnqFAwvaDt6pzMR8plJlv5B9NomNHSmYGfgzrAoLRlpPqtThWjbgyoMmJuX

