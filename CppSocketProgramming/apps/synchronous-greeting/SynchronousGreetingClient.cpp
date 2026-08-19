#include "ApplicationSupport.h"

#include <exception>
#include <iostream>

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::WinsockRuntime winsock;
        const auto settings = network::parse_client_settings(argc, argv, application::GreetingPort);
        const network::Socket server = network::connect_sync(settings.host, settings.port);

        network::send_line(server, application::GreetingRequest);
        application::print_received(network::remote_endpoint(server), network::receive_line(server));
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}

