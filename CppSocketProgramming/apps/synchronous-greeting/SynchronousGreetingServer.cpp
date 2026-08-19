#include "ApplicationSupport.h"

#include <exception>
#include <iostream>

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::WinsockRuntime winsock;
        const auto port = network::parse_server_port(argc, argv, application::GreetingPort);
        const network::Socket listener = network::create_listener(port);
        std::cout << "Synchronous greeting server is listening on 127.0.0.1:" << port << '\n';

        const network::Socket client = network::accept_sync(listener);
        const std::string endpoint = network::remote_endpoint(client);
        application::print_received(endpoint, network::receive_line(client));
        network::send_line(client, application::GreetingResponse);
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}

