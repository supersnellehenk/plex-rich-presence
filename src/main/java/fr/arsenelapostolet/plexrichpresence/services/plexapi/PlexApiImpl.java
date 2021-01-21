package fr.arsenelapostolet.plexrichpresence.services.plexapi;

import fr.arsenelapostolet.plexrichpresence.model.*;
import fr.arsenelapostolet.plexrichpresence.services.plexapi.plextv.PlexApiHttpClient;
import fr.arsenelapostolet.plexrichpresence.services.plexapi.plextv.PlexTokenHttpClient;
import fr.arsenelapostolet.plexrichpresence.services.plexapi.plextv.PlexTvAPI;
import fr.arsenelapostolet.plexrichpresence.services.plexapi.server.PlexSessionHttpClient;
import org.springframework.stereotype.Service;
import rx.Observable;

import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

@Service
public class PlexApiImpl implements PlexApi {

    public PlexApiImpl() {
    }



    @Override
    public Observable<List<Server>> getServers(String username, String password) {


        return new PlexApiHttpClient(username, password)
                .getAPI()
                .getServers()
                .map(MediaContainerServer::getServer)
                .map(servers -> servers.stream().filter(server -> {
                    String[] result = checkServer(server);
                    if (result[0].equals("success")) {
                        server.setFinalAddress(result[1]);
                        return true;
                    } else {
                        return false;
                    }
                }).collect(Collectors.toList()));

    }

    @Override
    public Observable<PlexLogin> getToken(String username, String password) {
        PlexTvAPI api = new PlexTokenHttpClient(username, password).getAPI();
        return api.login(username,password,
                        "Windows",
                        "10.0",
                        "Plex Rich Presence"
                );
    }

    @Override
    public Observable<List<Metadatum>> getSessions(List<Server> servers, String username) {

        List<Observable<PlexSessions>> sessionsObs =
                servers
                .stream()
                .map(server ->
                        new PlexSessionHttpClient(server.getFinalAddress(), server.getPort())
                                .getAPI()
                                .getSessions(server.getAccessToken(), "application/json"))
                .collect(Collectors.toList());

        return Observable.zip(sessionsObs, sessions -> Arrays.stream(sessions)
                .filter(obj -> obj instanceof PlexSessions)
                .map(obj -> (PlexSessions) obj)
                .flatMap(session -> session.getMediaContainer().getMetadata().stream())
                .filter(session -> session.getUser().getTitle().equals(username))
                .collect(Collectors.toList()));
    }

    private String[] checkServer(Server server) {
        String result[] = new String[2];
        String localAddr = server.getLocalAddresses();
        List<String> LocalServerAddresses = Arrays.asList(localAddr.split("\\s*,\\s*"));
        ;
        if (serverListening(server.getAddress(), Integer.parseInt(server.getPort()))) {
            result[0] = "success";
            result[1] = server.getAddress();
            return result;
        } else {
            for (String address : LocalServerAddresses) {
                if (serverListening(address, Integer.parseInt(server.getPort()))) {
                    result[0] = "success";
                    result[1] = address;
                    return result;
                }
            }
            result[0] = "fail";
            result[1] = null;
            return result;
        }
    }

    private Boolean serverListening(String host, int port) {
        Socket socket = new Socket();
        try {
            socket.connect(new InetSocketAddress(host, port), 1000);
            socket.close();
            return true;
        } catch (Exception e) {
            return false;
        }
    }

}
