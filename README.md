## Kacktaube

Crawls down osu!API v2 and Cheesegull API to create the best osu!Mirror experience possible.

## Requirements

#### Knowledge

You'll need Prior knowledge in C\# and .NET Core 3 if you want to use this Beatmap Mirror!

#### Dependencies

* [Docker \(For Production\)](https://www.docker.com/)
* [Dotnet Core 3.1](https://dotnet.microsoft.com)
* [ElasticSearch](https://www.elastic.co/de/)

## Setup Kacktaube

The easiest way would be docker I guess, simply run these commands \(!WARNING! DO NOT RUN UNTRUSTED COMMANDS IF YOU DON'T KNOW WHAT THEY DO!\):

```text
:~$ git clone https://github.com/Kacktaube/kacktaube.git
:~$ cd kacktaube
:~$ cp docker.env.example docker.env # PLEASE EDIT YOUR docker.env! as it's our config file
:~$ docker-compose up --build -d
```

I do not recommend setting it up though, as we don't want to make peppy mad by using too much of his valuable bandwidth.

That's why I've setup my own mirror at [kacktaube.me](https://kacktaube.me) where it basically stores EVERY SINGLE BEATMAP that has been downloaded at least once.
