FROM continuumio/miniconda3:4.7.10
# install spleeter engine dependencies (tensorflow, ffmpeg, ipython)
RUN conda install -y tensorflow==1.14.0
RUN conda  install -y -c conda-forge ffmpeg
RUN conda install -y -c anaconda pandas==0.25.1
RUN conda install -y -c conda-forge libsndfile
RUN conda install -y ipython

RUN mkdir /cache/
WORKDIR /workspace

# install asp.net core web api dependencies
RUN apt-get update 
RUN apt-get install -y gpg
RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.asc.gpg
RUN mv microsoft.asc.gpg /etc/apt/trusted.gpg.d/
RUN wget -q https://packages.microsoft.com/config/ubuntu/18.04/prod.list
RUN mv prod.list /etc/apt/sources.list.d/microsoft-prod.list
RUN chown root:root /etc/apt/trusted.gpg.d/microsoft.asc.gpg
RUN chown root:root /etc/apt/sources.list.d/microsoft-prod.list
RUN apt-get install -y apt-transport-https 
RUN apt-get update
RUN apt-get install -y dotnet-sdk-3.0

# install youtube-dl
RUN pip install youtube_dl

# clone and make deezer/spleeter
WORKDIR /workspace/deezer
RUN apt-get install -y git
RUN git clone https://github.com/deezer/spleeter
WORKDIR /workspace/deezer/spleeter/
RUN pip install --upgrade pip
RUN pip install .

# publish and run
WORKDIR /workspace/
RUN rm -rf deezer/
COPY ./ spleeter/

WORKDIR /workspace/spleeter/

ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000
RUN dotnet publish SpleeterAPI.csproj -o publish

ENTRYPOINT ["dotnet", "publish/SpleeterAPI.dll"]